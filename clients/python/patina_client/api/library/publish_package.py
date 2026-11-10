from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.package_published import PackagePublished
from ...models.package_request import PackageRequest
from ...models.problem_details import ProblemDetails
from typing import cast



def _get_kwargs(
    *,
    body: PackageRequest,

) -> dict[str, Any]:
    headers: dict[str, Any] = {}


    

    

    _kwargs: dict[str, Any] = {
        "method": "post",
        "url": "/api/v1/library/packages",
    }

    _kwargs["json"] = body.to_dict()

    headers["Content-Type"] = "application/json"

    _kwargs["headers"] = headers
    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> PackagePublished | ProblemDetails | None:
    if response.status_code == 200:
        response_200 = PackagePublished.from_dict(response.json())



        return response_200

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


def _build_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> Response[PackagePublished | ProblemDetails]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: PackageRequest,

) -> Response[PackagePublished | ProblemDetails]:
    """ Publishes a saved graph as a package in the library's packages folder, replacing one of its name.
    Every project's shelf then offers it.

    Args:
        body (PackageRequest): The body of `POST /api/v1/library/packages`.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[PackagePublished | ProblemDetails]
     """


    kwargs = _get_kwargs(
        body=body,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    *,
    client: AuthenticatedClient | Client,
    body: PackageRequest,

) -> PackagePublished | ProblemDetails | None:
    """ Publishes a saved graph as a package in the library's packages folder, replacing one of its name.
    Every project's shelf then offers it.

    Args:
        body (PackageRequest): The body of `POST /api/v1/library/packages`.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        PackagePublished | ProblemDetails
     """


    return sync_detailed(
        client=client,
body=body,

    ).parsed

async def asyncio_detailed(
    *,
    client: AuthenticatedClient | Client,
    body: PackageRequest,

) -> Response[PackagePublished | ProblemDetails]:
    """ Publishes a saved graph as a package in the library's packages folder, replacing one of its name.
    Every project's shelf then offers it.

    Args:
        body (PackageRequest): The body of `POST /api/v1/library/packages`.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[PackagePublished | ProblemDetails]
     """


    kwargs = _get_kwargs(
        body=body,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    *,
    client: AuthenticatedClient | Client,
    body: PackageRequest,

) -> PackagePublished | ProblemDetails | None:
    """ Publishes a saved graph as a package in the library's packages folder, replacing one of its name.
    Every project's shelf then offers it.

    Args:
        body (PackageRequest): The body of `POST /api/v1/library/packages`.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        PackagePublished | ProblemDetails
     """


    return (await asyncio_detailed(
        client=client,
body=body,

    )).parsed
