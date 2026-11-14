from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx2

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.preview_save_request import PreviewSaveRequest
from ...models.problem_details import ProblemDetails
from typing import cast



def _get_kwargs(
    hash_: str,
    *,
    body: PreviewSaveRequest,

) -> dict[str, Any]:
    headers: dict[str, Any] = {}


    

    

    _kwargs: dict[str, Any] = {
        "method": "post",
        "url": "/api/v1/previews/{hash_}/save".format(hash_=quote(str(hash_), safe=""),),
    }

    _kwargs["json"] = body.to_dict()

    headers["Content-Type"] = "application/json"

    _kwargs["headers"] = headers
    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> Any | ProblemDetails | None:
    if response.status_code == 204:
        response_204 = cast(Any, None)
        return response_204

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if response.status_code == 422:
        response_422 = ProblemDetails.from_dict(response.json())



        return response_422

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> Response[Any | ProblemDetails]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    hash_: str,
    *,
    client: AuthenticatedClient | Client,
    body: PreviewSaveRequest,

) -> Response[Any | ProblemDetails]:
    """ Writes a picture the Server has written, by its hash, to a PNG file on the Server's machine,
    replacing one there. POST /graphs/{id}/images writes a node's picture at full size first.

    Args:
        hash_ (str):
        body (PreviewSaveRequest): The body of `POST /api/v1/previews/{hash}/save`.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[Any | ProblemDetails]
     """


    kwargs = _get_kwargs(
        hash_=hash_,
body=body,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    hash_: str,
    *,
    client: AuthenticatedClient | Client,
    body: PreviewSaveRequest,

) -> Any | ProblemDetails | None:
    """ Writes a picture the Server has written, by its hash, to a PNG file on the Server's machine,
    replacing one there. POST /graphs/{id}/images writes a node's picture at full size first.

    Args:
        hash_ (str):
        body (PreviewSaveRequest): The body of `POST /api/v1/previews/{hash}/save`.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Any | ProblemDetails
     """


    return sync_detailed(
        hash_=hash_,
client=client,
body=body,

    ).parsed

async def asyncio_detailed(
    hash_: str,
    *,
    client: AuthenticatedClient | Client,
    body: PreviewSaveRequest,

) -> Response[Any | ProblemDetails]:
    """ Writes a picture the Server has written, by its hash, to a PNG file on the Server's machine,
    replacing one there. POST /graphs/{id}/images writes a node's picture at full size first.

    Args:
        hash_ (str):
        body (PreviewSaveRequest): The body of `POST /api/v1/previews/{hash}/save`.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[Any | ProblemDetails]
     """


    kwargs = _get_kwargs(
        hash_=hash_,
body=body,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    hash_: str,
    *,
    client: AuthenticatedClient | Client,
    body: PreviewSaveRequest,

) -> Any | ProblemDetails | None:
    """ Writes a picture the Server has written, by its hash, to a PNG file on the Server's machine,
    replacing one there. POST /graphs/{id}/images writes a node's picture at full size first.

    Args:
        hash_ (str):
        body (PreviewSaveRequest): The body of `POST /api/v1/previews/{hash}/save`.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Any | ProblemDetails
     """


    return (await asyncio_detailed(
        hash_=hash_,
client=client,
body=body,

    )).parsed
