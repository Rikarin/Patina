from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx2

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.problem_details import ProblemDetails
from ...types import UNSET, Unset
from typing import cast



def _get_kwargs(
    *,
    path: str,
    side: int | Unset = UNSET,

) -> dict[str, Any]:
    

    

    params: dict[str, Any] = {}

    params["path"] = path

    params["side"] = side


    params = {k: v for k, v in params.items() if v is not UNSET and v is not None}


    _kwargs: dict[str, Any] = {
        "method": "get",
        "url": "/api/v1/library/picture",
        "params": params,
    }


    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> ProblemDetails | None:
    if response.status_code == 400:
        response_400 = ProblemDetails.from_dict(response.json())



        return response_400

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if response.status_code == 409:
        response_409 = ProblemDetails.from_dict(response.json())



        return response_409

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> Response[ProblemDetails]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    *,
    client: AuthenticatedClient | Client,
    path: str,
    side: int | Unset = UNSET,

) -> Response[ProblemDetails]:
    """ A picture the library holds, as a client can show it: a PNG or a JPEG as its file, and any other
    picture as an 8-bit PNG of it, such as a TIFF, a TGA, a PSD or an EXR (#260). With side, any picture
    as a PNG whose longer side is at most that many texels, for a thumbnail (#269).

    Args:
        path (str):
        side (int | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails]
     """


    kwargs = _get_kwargs(
        path=path,
side=side,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    *,
    client: AuthenticatedClient | Client,
    path: str,
    side: int | Unset = UNSET,

) -> ProblemDetails | None:
    """ A picture the library holds, as a client can show it: a PNG or a JPEG as its file, and any other
    picture as an 8-bit PNG of it, such as a TIFF, a TGA, a PSD or an EXR (#260). With side, any picture
    as a PNG whose longer side is at most that many texels, for a thumbnail (#269).

    Args:
        path (str):
        side (int | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails
     """


    return sync_detailed(
        client=client,
path=path,
side=side,

    ).parsed

async def asyncio_detailed(
    *,
    client: AuthenticatedClient | Client,
    path: str,
    side: int | Unset = UNSET,

) -> Response[ProblemDetails]:
    """ A picture the library holds, as a client can show it: a PNG or a JPEG as its file, and any other
    picture as an 8-bit PNG of it, such as a TIFF, a TGA, a PSD or an EXR (#260). With side, any picture
    as a PNG whose longer side is at most that many texels, for a thumbnail (#269).

    Args:
        path (str):
        side (int | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails]
     """


    kwargs = _get_kwargs(
        path=path,
side=side,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    *,
    client: AuthenticatedClient | Client,
    path: str,
    side: int | Unset = UNSET,

) -> ProblemDetails | None:
    """ A picture the library holds, as a client can show it: a PNG or a JPEG as its file, and any other
    picture as an 8-bit PNG of it, such as a TIFF, a TGA, a PSD or an EXR (#260). With side, any picture
    as a PNG whose longer side is at most that many texels, for a thumbnail (#269).

    Args:
        path (str):
        side (int | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails
     """


    return (await asyncio_detailed(
        client=client,
path=path,
side=side,

    )).parsed
